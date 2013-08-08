/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-8.js
 * @description JSON.stringify correctly works on top level String objects.
 */


function testcase() {
  return JSON.stringify(new String('wrappered')) === '"wrappered"';
  }
runTestCase(testcase);
