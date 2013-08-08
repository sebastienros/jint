/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-12-1.js
 * @description Accessing callee property of Arguments object is allowed
 */


function testcase() {
  try 
  {
    arguments.callee;
    return true;
  }
  catch (e) {
  }
 }
runTestCase(testcase);
