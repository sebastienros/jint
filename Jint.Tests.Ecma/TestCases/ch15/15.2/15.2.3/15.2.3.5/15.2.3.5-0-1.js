/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-0-1.js
 * @description Object.create must exist as a function
 */


function testcase() {
  if (typeof(Object.create) === "function") {
    return true;
  }
 }
runTestCase(testcase);
