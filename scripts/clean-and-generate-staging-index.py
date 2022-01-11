from urllib.request import urlopen
from datetime import datetime, timezone
import json
import html
import os
import shutil
from math import trunc

project_id = "10819053"
web_dir = "C:\\UtilityBelt\\www\\"

template = """
<!DOCTYPE html>
<html>
  <head>
    <title>UtilityBelt Staging</title>
    <style>
        body {
          background-color: #d3d3d3;
          color: #222;
        }

        a {
          color: #008AFF;
        }

        a.success {
            font-weight: bold;
            color: green;
        }

        a.failed {
            font-weight: bold;
            color: red;
        }

        h3 {
          font-size: 26px;
          padding: 0;
          margin: 0;
          font-weight: bold;
        }

        h4 {
          font-size: 16px;
          padding: 0;
          margin: 8px 0;
          font-weight: normal;
        }
        
        div.content {
          margin: 0 auto;
          width: 700px;
        }

        div.branch {
          background-color: #f5f5f5;
          padding: 8px 14px;
          margin-bottom: 10px;
          border: 1px solid #999;
        }

        body div.branch-master {
          background-color: #ddf3ff;
        }
        
        div.branch p {
          margin: 0;
          padding: 8px 0;
        }
    </style>
  </head>
  <body>
    <div class="content">
        <h1>UtilityBelt Beta Branches</h1>
        <p>These are automatically updated every time a branch is pushed to <a href="https://gitlab.com/utilitybelt/utilitybelt.gitlab.io" target="_blank">https://gitlab.com/utilitybelt/utilitybelt.gitlab.io</a></p>

        %CONTENTS%
    </div>
    <script type="text/javascript">
      function timeSince(date) {
        var seconds = Math.floor((new Date() - date) / 1000);

        var interval = seconds / 31536000;

        if (interval > 1) {
          return Math.floor(interval) + " years ago";
        }
        interval = seconds / 2592000;
        if (interval > 1) {
          return Math.floor(interval) + " months ago";
        }
        interval = seconds / 86400;
        if (interval > 1) {
          return Math.floor(interval) + " days ago";
        }
        interval = seconds / 3600;
        if (interval > 1) {
          return Math.floor(interval) + " hours ago";
        }
        interval = seconds / 60;
        if (interval > 1) {
          return Math.floor(interval) + " minutes ago";
        }
        return Math.floor(seconds) + " seconds ago";
      }

      var els = document.getElementsByClassName("timeago");
      for (var i = 0; i < els.length; i++) {
        var d = Date.parse(els.item(i).textContent);
        els.item(i).textContent = timeSince(d)
      }
    </script>
  </body>
</html>
"""


response = urlopen("https://gitlab.com/api/v4/projects/" + project_id + "/repository/branches")
data = json.loads(response.read())

contents = ""

def get_pipeline_status(branch):
    status = "unknown"
    response = urlopen("https://gitlab.com/api/v4/projects/" + project_id + "/pipelines/?ref=" + branch['name'])
    data = json.loads(response.read())
    if data and len(data):
        s = data[0]['status']
        if s == "running":
            s = "success"
        status = "<a href=\"" + data[0]['web_url'] + "\" class=\"" + s + "\">" + s + "</a>"
    return status

def sortBranches(e):
    # 2020-12-11T07:15:49.000+00:00
    if e['name'] == "master":
      return datetime.strptime("January 1, 3000", "%B %d, %Y").timestamp()

    return datetime.strptime(e['commit']['created_at'], '%Y-%m-%dT%H:%M:%S.%f%z').timestamp()

data.sort(key=sortBranches, reverse=True)

valid_branches = []
subfolders = [ f.name for f in os.scandir(web_dir) if f.is_dir() ]

did_master = False

for branch in data:
    branch_class = ""
    if branch['name'] == "master":
        branch_class = "branch-master"

    if branch['name'] not in subfolders:
        continue

    valid_branches.append(branch['name'])
    pipeline_status = get_pipeline_status(branch)

    print("Found branch: " + branch['name'])
    contents = contents + "<div class=\"branch " + branch_class + "\">\n"
    contents = contents + "<h3>" + branch['name'] + "</h3>\n"
    contents = contents + "<h4><strong>Status:</strong> " + pipeline_status + " | <strong>Updated:</strong> <span class=\"timeago\">" + branch['commit']['created_at'] + "</span></h4>\n"
    contents = contents + "<p><strong>Last Commit:</strong> " + html.escape(branch['commit']['message']) + "</p>\n"
    contents = contents + "<div class=\"links\"><strong>Links:</strong> "
    #contents = contents + "<a href=\"#\">Installer</a> | "
    contents = contents + "<a href=\"/" + branch['name'] + "/\">Beta Documentation Site</a> | "
    contents = contents + "<a href=\"" + branch['web_url'] + "\">GitLab Branch</a>"
    contents = contents + "</div>\n"
    contents = contents + "</div>\n\n"

    if not did_master:
        contents = contents + "<br /><br />";
        did_master = True

print("Generating index.html with branches: " + ', '.join(valid_branches))
f = open(web_dir + "index.html", "w")
f.write(template.replace("%CONTENTS%", contents))
f.close()

print("Generating robots.txt")
f = open(web_dir + "robots.txt", "w")
f.write("User-agent: *\nDisallow: /")
f.close()

for sf in subfolders:
    if sf not in valid_branches:
        print("Removing stale branch: " + sf)
        shutil.rmtree(web_dir + sf, ignore_errors=True) 