/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-2.js
 * @description Object.isExtensible must exist as a function taking 1 parameter
 */


function testcase() {
  if (Object.isExtensible.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
