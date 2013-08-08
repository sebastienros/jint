/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-5gs.js
 * @description Strict Mode - Use Strict Directive Prologue is ''use strict';' which appears at the start of the code
 * @noStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
var public = 1;
