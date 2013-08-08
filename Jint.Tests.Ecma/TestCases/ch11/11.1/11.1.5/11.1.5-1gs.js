/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.1/11.1.5/11.1.5-1gs.js
 * @description Strict Mode - SyntaxError is thrown when 'eval' occurs as the Identifier in a PropertySetParameterList of a PropertyAssignment that is contained in strict code
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
var obj = { set _11_1_5_1_fun(eval) {}};
