/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-0-1.js
 * @description Object.isExtensible must exist as a function
 */


function testcase() {
  var f = Object.isExtensible ;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
