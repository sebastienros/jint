/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-5.js
 * @description JSON.stringify correctly works on top level Boolean values.
 */


function testcase() {
  return JSON.stringify(true) === 'true';
  }
runTestCase(testcase);
