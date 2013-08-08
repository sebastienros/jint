/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-10-s.js
 * @description Strict Mode: an indirect eval assigning into 'eval' does not throw
 * @onlyStrict
 */


function testcase() {
  'use strict';
  var s = eval;
  s('eval = 42;');
  return true;
 }
runTestCase(testcase);
