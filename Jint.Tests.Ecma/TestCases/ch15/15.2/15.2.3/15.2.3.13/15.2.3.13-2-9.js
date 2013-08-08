/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-9.js
 * @description Object.isExtensible returns true for all built-in objects (Date)
 */


function testcase() {
  var e = Object.isExtensible(Date);
  if (e === true) {
    return true;
  }
 }
runTestCase(testcase);
