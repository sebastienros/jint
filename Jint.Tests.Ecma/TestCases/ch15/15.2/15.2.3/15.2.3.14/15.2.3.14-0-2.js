/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-0-2.js
 * @description Object.keys must exist as a function taking 1 parameter
 */


function testcase() {
  if (Object.keys.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
