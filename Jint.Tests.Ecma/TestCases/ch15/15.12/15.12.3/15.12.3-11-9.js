/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-9.js
 * @description JSON.stringify correctly works on top level Boolean objects.
 */


function testcase() {
  return JSON.stringify(new Boolean(false)) === 'false';
  }
runTestCase(testcase);
