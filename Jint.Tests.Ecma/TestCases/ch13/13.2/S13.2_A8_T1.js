// Copyright 2011 Google Inc.  All rights reserved.
/**
 * @path ch13/13.2/S13.2_A8_T1.js
 * @description check if "caller" poisoning poisons
 * "in" too
 * @onlyStrict
 */

"use strict";
'caller' in function() {};


