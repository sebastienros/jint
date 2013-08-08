/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-8gs.js
 * @description Strict Mode - SyntaxError is thrown if a FunctionExpression has two identical parameters
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
var _13_1_8_fun = function (param, param) { };