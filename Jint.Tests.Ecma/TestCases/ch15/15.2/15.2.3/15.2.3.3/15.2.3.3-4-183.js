/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-183.js
 * @description Object.getOwnPropertyDescriptor returns undefined for non-existent property (arguments_1) on built-in object (Function)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Function, "arguments_1");

  if (desc === undefined)
    return true;
  else
    return false;  
 }
runTestCase(testcase);
