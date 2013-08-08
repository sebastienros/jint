/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-21-s.js
 * @description Strict Mode: an indirect eval assigning into 'arguments' does not throw
 * @onlyStrict
 */




function testcase() {
  'use strict';
  var s = eval;
  s('arguments = 42;');
  return true;
}
runTestCase(testcase);