/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-3.js
 * @description A JSON.stringify correctly works on top level string values.
 */


function testcase() {
  return JSON.stringify("a string") === '"a string"';
  }
runTestCase(testcase);
