/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-c-1-s.js
 * @description Accessing callee property of Arguments object throws TypeError in strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';
  try 
  {
    arguments.callee;
    return false;
  }
  catch (e) {
    return (e instanceof TypeError);
  }
 }
runTestCase(testcase);
