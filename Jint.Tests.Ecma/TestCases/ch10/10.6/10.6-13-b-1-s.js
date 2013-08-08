/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-b-1-s.js
 * @description Accessing caller property of Arguments object throws TypeError in strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';
  try 
  {
    arguments.caller;
  }
  catch (e) {
    if(e instanceof TypeError)
      return true;
  }
 }
runTestCase(testcase);
