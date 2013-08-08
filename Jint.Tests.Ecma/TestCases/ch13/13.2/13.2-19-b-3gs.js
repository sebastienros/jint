/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-19-b-3gs.js
 * @description StrictMode - error is thrown when assign a value to the 'caller' property of a function object
 * @onlyStrict
 * @negative NotEarlyError
 */
"use strict";
throw NotEarlyError;
function _13_2_19_b_3_gs() {}
_13_2_19_b_3_gs.caller = 1;
