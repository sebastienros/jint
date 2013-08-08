/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-5gs.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable which is primitive type(boolean)
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
var _11_4_1_5 = 7;
throw NotEarlyError;
delete _11_4_1_5;