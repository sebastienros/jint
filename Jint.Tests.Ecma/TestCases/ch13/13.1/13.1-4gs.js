/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-4gs.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'arguments' appears within a FormalParameterList of a strict mode FunctionExpression
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
var _13_1_4_fun = function (arguments) { };