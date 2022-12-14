# UtilityBelt

UtilityBelt is a multipurpose decal plugin for Asheron’s Call that provides a number of different tools.

For documentation and latest install files, check out [http://utilitybelt.gitlab.io/](http://utilitybelt.gitlab.io)

Have a feature / suggestion / bug report? Join us in [Discord](https://discord.gg/c75pPaz)

## Building

- Update to decal beta (you can build without updating, but you wont be able to load the plugin)
- Pull latest master
- If you have previously built this project:
  - Delete UtilityBelt/Properties/AssemblyInfo.cs
  - Delete UBLoader/Properties/AssemblyInfo.cs
  - Delete UtilityBelt/obj/
  - Delete UBLoader/obj/
  - Delete bin/ (just to clean things up... not required)
- Build all projects
- Run installer in bin/
  - Choose bin/Release/net48/ as install directory for UB
  - A child installer for UtilityBelt.Service will also pop up
    - Choose UtilityBelt.Service/bin/Release/net48/ if you have the project checked out, otherwise you can leave it default.  (Just dont install this to your UB install directory).

## Contributing

Pull requests are welcome. For major changes, please open an issue / join [Discord](https://discord.gg/c75pPaz) first to discuss what you would like to change.

## Contributors

* Aquafir
* Brycter
* Cosmic Jester
* dpbarrett
* FlaggAC
* enknamel
* Harli
* Schneebly
* trevis
* Yonneh

## 📄 License

MIT
