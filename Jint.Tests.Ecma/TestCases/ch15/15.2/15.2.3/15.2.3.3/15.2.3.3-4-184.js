/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-184.js
 * @description Object.getOwnPropertyDescriptor returns undefined for non-existent property (caller) on built-in object (Math)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Math, "caller");

  if (desc === undefined)
    return true;  
  else
    return false;
 }
runTestCase(testcase);
