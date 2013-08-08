/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-6-2.js
 * @description 'length' property of arguments object has correct attributes
 */


function testcase() {
  
  var desc = Object.getOwnPropertyDescriptor(arguments,"length");
  if(desc.configurable === true &&
     desc.enumerable === false &&
     desc.writable === true )
    return true;
 }
runTestCase(testcase);
