/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-4gs.js
 * @description StrictMode - a Use Strict Directive followed by a strict mode violation
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"use strict";
throw NotEarlyError;
eval = 42;