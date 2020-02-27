### Summary
UtilityBelt includes functionality to override the built-in expression language runner inside of VTank.  This enables new meta expression functions and language features to be added while still running metas inside of VTank.

To enable this functionality, you need to set UtilityBelt option`VTank.PatchExpressionEngine` to `true`, you can do that with `/ub opt set VTank.PatchExpressionEngine true`.

Once enabled, UtilityBelt will handle all meta conditions of type `Chat Message Capture` and `Expression`, meta actions of type `Expression Action` and `Chat Expression`, as well as meta view button expressions.

UtilityBelt is completely backwards compatible with VTank meta expressions, so any existing expressions should *just work*.  Keeping that in mind, you can view documentation for VTank's meta expressions [here](http://www.virindi.net/wiki/index.php/Meta_Expressions).

Below is a list of all expression functions implemented in UtilityBelt.