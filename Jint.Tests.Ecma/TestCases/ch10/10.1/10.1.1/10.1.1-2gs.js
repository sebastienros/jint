/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-2gs.js
 * @description Strict Mode - Use Strict Directive Prologue is ''use strict'' which lost the last character ';'
 * @noStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict"
throw NotEarlyError;
var public = 1;
