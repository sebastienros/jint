/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.1/11.1.5/11.1.5-2gs.js
 * @description Strict Mode - SyntaxError is thrown when eval code contains an ObjectLiteral with more than one definition of any data property
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
var obj = { _11_1_5_2_gs: 10, _11_1_5_2_gs: 10 };
