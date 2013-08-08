/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-6-1.js
 * @description 'length property of arguments object exists
 */


function testcase() {
  
  var desc = Object.getOwnPropertyDescriptor(arguments,"length");
  return desc !== undefined
 }
runTestCase(testcase);
