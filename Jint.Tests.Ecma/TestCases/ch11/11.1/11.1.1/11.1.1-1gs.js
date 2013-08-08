/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.1/11.1.1/11.1.1-1gs.js
 * @description Strict Mode - 'this' object at the global scope is not undefined
 * @onlyStrict
 */

"use strict";
if (this===undefined) {
    throw NotEarlyError;
}
