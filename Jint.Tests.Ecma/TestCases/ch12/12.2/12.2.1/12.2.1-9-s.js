/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-9-s.js
 * @description Strict Mode: an indirect eval declaring a var named 'eval' does not throw
 * @onlyStrict
 */


function testcase() {
  'use strict';
  var s = eval;
  s('var eval;');
  return true;
 }
runTestCase(testcase);
