/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-1.js
 * @description Accessing caller property of Arguments object is allowed
 */


function testcase() {
  try 
  {
    arguments.caller;
    return true;
  }
  catch (e) {
  }
 }
runTestCase(testcase);
