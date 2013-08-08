/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.3/7.8.3-1gs.js
 * @description Strict Mode - octal extension(010) is forbidden in strict mode
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
var y = 010;
