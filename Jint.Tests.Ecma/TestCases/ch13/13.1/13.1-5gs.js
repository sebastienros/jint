/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-5gs.js
 * @description Strict Mode - SyntaxError is thrown if a FunctionDeclaration has two identical parameters
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
function _13_1_5_fun(param, param) { }