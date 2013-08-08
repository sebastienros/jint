// Copyright 2011 Google Inc.  All rights reserved.
/**
 * @path ch13/13.2/S13.2_A6_T2.js
 * @description check if "arguments" poisoning poisons
 * getOwnPropertyDescriptor too
 * @onlyStrict
 */

"use strict";
Object.getOwnPropertyDescriptor(function(){}, 'arguments');

