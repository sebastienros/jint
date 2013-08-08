/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.0/13.0_4-17gs.js
 * @description Strict Mode - SourceElements is not evaluated as strict mode code when a Function constructor is contained in strict mode code
 * @onlyStrict
 * @negative NotEarlyError
 */

"use strict";
var _13_0_4_17_fun = new Function('eval = 42;');
throw NotEarlyError;
