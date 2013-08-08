/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-3-1-s.js
 * @description eval - a property named 'eval' is permitted
 * @onlyStrict
 */


function testcase() {
  'use strict';

  var o = { eval: 42};
  return true;
 }
runTestCase(testcase);
