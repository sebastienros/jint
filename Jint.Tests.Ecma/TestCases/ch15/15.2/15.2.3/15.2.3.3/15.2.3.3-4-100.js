/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-100.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Math.atan2)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Math, "atan2");
  if (desc.value === Math.atan2 &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
