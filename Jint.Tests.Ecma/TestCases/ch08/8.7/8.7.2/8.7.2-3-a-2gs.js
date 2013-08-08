/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-3-a-2gs.js
 * @description Strict Mode - 'runtime' error is thrown before LeftHandSide evaluates to an unresolvable Reference
 * @onlyStrict
 * @negative NotEarlyError
 */

"use strict";
throw NotEarlyError;
b = 11;
