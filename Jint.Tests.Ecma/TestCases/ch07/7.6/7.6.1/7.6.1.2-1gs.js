/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2-1gs.js
 * @description Strict Mode - SyntaxError is thrown when FutureReservedWord 'implements' occurs in strict mode code
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
var implements = 1;
