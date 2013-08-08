// Copyright 2011 Google Inc.  All rights reserved.
/**
 * @path ch13/13.2/S13.2_A7_T1.js
 * @description check if "caller" poisoning poisons
 * hasOwnProperty too
 * @onlyStrict
 */

"use strict";
(function(){}).hasOwnProperty('caller');


