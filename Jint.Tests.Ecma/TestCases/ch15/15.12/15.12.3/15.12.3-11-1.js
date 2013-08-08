/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-1.js
 * @description JSON.stringify(undefined) returns undefined
 */


function testcase() {
  return JSON.stringify(undefined) === undefined;
  }
runTestCase(testcase);
