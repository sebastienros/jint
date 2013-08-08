/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.1/11.13.1-4-29gs.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'Math.PI' appears as the LeftHandSideExpression of simple assignment(=)
 * @onlyStrict
 * @negative .
 */
"use strict";
Math.PI = 20;
