### Summary
UtilityBelt includes functionality to override the built-in expression language runner inside of VTank.  This enables new meta expression functions and language features to be added while still running metas inside of VTank.

To enable this functionality, you need to set UtilityBelt option`VTank.PatchExpressionEngine` to `true`, you can do that with `/ub opt set VTank.PatchExpressionEngine true`.

Once enabled, UtilityBelt will handle all meta conditions of type `Chat Message Capture` and `Expression`, meta actions of type `Expression Action` and `Chat Expression`, as well as meta view button expressions.

UtilityBelt is completely backwards compatible with VTank meta expressions, so any existing expressions should *just work*.  Keeping that in mind, you can view documentation for VTank's meta expressions [here](http://www.virindi.net/wiki/index.php/Meta_Expressions).

### Language Overview
The expression language offers functionality to control and read game client state, as well as some basic logic operations.  There is support for a number of data types, as well as operators and functions to use on those types.

### Data Types
* **Number** - Numbers are stored internally as doubles. They have a precision of 15-16 digits and can store a range from +-5.0 x 10<sup>-324</sup>  to +-1.7 x 10<sup>308</sup>
	* Numbers can also be defined in hexadecimal format, ie `0xff`
* **String** - A string of characters, can be any length.
	* Strings containing anything other than letters and spaces need to be escaped with backslashes escaping individual characters, or backticks surrounding the entire string. Examples:
		* `123 test` should be escaped as `\1\2\3 test` or `` `123 test` ``
		* `some example string` does not need escaping
		* `p@cMan$)`  should be escaped as `p\@cMan\$\)` or `` `p@cMan$` ``
* **List** - A set of items. Items can be of any type.
* **Coordinates** - Represents a set of NS/EW/Z game coordinates.
* **WorldObject** - Represents a game object (player, item, monster, npc, etc)
* **StopWatch** - Time based counter, with the ability to stop/start.

### Operators
* **`==`** Checks if two objects are equal to each other.  They must be of the same type.
* **`>`** Checks if a value is greater than another. This only works with numbers.
* **`<`** Checks if a value is less than another. This only works with numbers.
* **`>=`** Checks if a value is greater than or equal to another. This only works with numbers.
* **`<=`** Checks if a value is less than or equal to another. This only works with numbers.
* **`/`** Divides one value by another. This only works with numbers.
* **`*`** Multiplies one value by another. This only works with numbers.
* **`+`** Adds one value to another. This only works with numbers and strings.
* **`-`** Subtracts one value from another. This only works with numbers.
* **`%`** Determines the remainder of the division of one value from another. This only works with numbers.
* **`#`** Evaluates to true if the first value matches the passed regular expression. Example: `test#s` checks if the string `test` matches the regex `s`. Regexes used here are case-insensitive when matching. This only works with strings.
* **`&&`** Evaluates to true if both the expression before and after the operator return true
* **`||`** Evaluates to true if either the expression before or after the operator returns true

### Expressions (Functions)
Expression functions, also referred to as just expressions, are used to perform actions or operate on data passed to them.  They are all called using the same syntax: `expressionname[]`. The square brackets are used to enclose arguments, even when there are no arguments they are required. 

Some expressions will require arguments to be passed to them, like `echo[hello, 15]`.  Passed arguments are separated with the `,` character. The `echo` expression expects two arguments to be passed: a string, and a number indicating the color the string will be printed in. In the example above, `hello` is the passed string, and `15` is the passed number. This expression echoes the passed string to the chat window in the specified color.

Other expressions may not require arguments and instead of performing an action, return data. For example, the expression `wobjectgetplayer[]` will refer to your currect player character.  The `wobjectgetplayer` expression doesn't expect any arguments to be passed to it. To indicate this, the brackets should still be present but should have nothing within them.

You can pass the return value of an expression to another expression and chain them together. For example, to get the name of the current player we need to pass the result of one expression to another: `wobjectgetname[wobjectgetplayer[]]`.  In this example, the `wobjectgetname` expression expects to be passed a single argument of type WorldObject.  Since the `wobjectgetplayer` expression returns a `WorldObject` we can pass the result of it to `wobjectgetname`.

### Variables
Variables are used to store values, and retrieve them at a later time. There are three types of variables:

* &nbsp;**Memory** - This is the default variable type. These variables only exist while the character is logged in.  Once it is logged out, all variables of this type are lost.
* &nbsp;**Persistent** - These variable types are only accessible by the character who set them, but they are serialized to a database so they can be restored when the client is restarted.
* &nbsp;**Global** - These variable types can be accessed by any running client, and are stored to the database any time they are modified.

Memory variables are good for quickly storing the value of something to be used later in that same client session. If your meta needs to save state to be accessed even after the client has closed, use a persistent variable. If the variable needs to be accessed and shared with all clients/characters, use a global variable.

You can set a memory variable using the `setvar` expression.  It expects two arguments, the first being a string of the name to store the variable under, and the second being the value to store.  For example `setvar[myVariable,123]` would store the number value `123` as a variable named `myVariable`.  All data types can be stored in a memory variable.  To read the value stored in a variable, you use the expression `getvar`. It expects one argument: a string of the variable name. For example to retrieve the memory variable we stored above we can use `getvar[myVariable]`, this will return the stored number `123`.  There is an alternate (shorter) syntax for getting a memory variable, `$`.  For example to get the same value stored above, you could use the alternate syntax: `$myVariable`.

Persistent and global variables have their own methods/syntax for getting and setting. Persistent variables use `setpvar` to set, `getpvar` to read, and supports the alternate syntax `@` for getting variables (`@myPersistentVariable` returns the persistent variable named `myPersistentVariable`).  Global variables use `setgvar` to set, `getgvar` to read, and supports the alternate syntax `&` for getting variables (`&myGlobalVariable` returns the global variable named `myGlobalVariable`)

### Expression Examples
* `coordinatedistancewithz[getplayercoordinates[], wobjectgetphysicscoordinates[wobjectgetselection[]]]`
	* Gets the distance from your player to the currently selected object
* `setvar[myList,createlist[1,2,3]]; listadd[$myList,4]`
	* Saves a newly created list with the values `1,2,3` as a memory variable called `myList`. Then adds the value `4` to that same list using the `$` getvar syntax.


### Notes
* Although there are keywords for true/false, internally they represent number 1/0.
* Methods that return `WorldObject` types will return `0` if no matching objects were found.  This will throw an error when passing the results to another expression that expects a `WorldObject`. It is recommended to use the `getobjectinternaltype` expression to check the resulting type before usage.
* The alternative get variable syntax (`$`, `@`, and `&`) supports expressions where the variable identifier is. For example you can do the following to get a variable named `Sunnuj`, assuming that is your current character name: `$wobjectgetname[wobjectgetplayer[]]`.  It is equivalent to writing `getvar[wobjectgetname[wobjectgetplayer[]]]`.

### Expressions
