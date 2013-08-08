/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-1gs.js
 * @description Strict Mode - SyntaxError is thrown if a VariableDeclaration occurs within strict code and its Identifier is eval
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
for (var eval in arrObj) { }