/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-20.js
 * @description Object.getOwnPropertyDescriptor returns data desc for functions on built-ins (Object.seal)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Object, "seal");
  if (desc.value === Object.seal &&
      desc.writable === true &&
      desc.enumerable === false &&
      desc.configurable === true) {
    return true;
  }
 }
runTestCase(testcase);
