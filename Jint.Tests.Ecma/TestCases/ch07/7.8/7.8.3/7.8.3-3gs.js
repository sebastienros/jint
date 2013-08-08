/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.3/7.8.3-3gs.js
 * @description Strict Mode - octal extension is forbidden in strict mode (after a hex number is assigned to a variable from an eval)
 * @onlyStrict
 * @negative SyntaxError
 */

"use strict";
var a;
eval("a = 0x1;a = 01;");
