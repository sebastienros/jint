/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-12-2.js
 * @description arguments.callee has correct attributes
 */


function testcase() {
  
  var desc = Object.getOwnPropertyDescriptor(arguments,"callee");
  if(desc.configurable === true &&
     desc.enumerable === false &&
     desc.writable === true &&
     desc.hasOwnProperty('get') == false &&
     desc.hasOwnProperty('put') == false)
    return true;   
 }
runTestCase(testcase);
