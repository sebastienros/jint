/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-1gs.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'eval' appears within a FormalParameterList of a strict mode FunctionDeclaration
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
function _13_1_1_fun(eval) { }