/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-4.js
 * @description JSON.stringify correctly works on top level Number values.
 */


function testcase() {
  return JSON.stringify(123) === '123';
  }
runTestCase(testcase);
