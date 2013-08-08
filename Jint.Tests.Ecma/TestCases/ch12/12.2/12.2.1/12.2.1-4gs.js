/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-4gs.js
 * @description Strict Mode - SyntaxError is thrown if a VariableDeclarationNoIn occurs within strict code and its Identifier is arguments
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
var arguments;