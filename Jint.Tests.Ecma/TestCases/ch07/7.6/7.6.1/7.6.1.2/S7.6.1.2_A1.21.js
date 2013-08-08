// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The "package" token can not be used as identifier in strict code
 *
 * @path ch07/7.6/7.6.1/7.6.1.2/S7.6.1.2_A1.21.js
 * @description Checking if execution of "package=1" fails in strict code
 * @onlyStrict
 * @negative
 */

"use strict";
var package = 1;

