/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.3/15.3.3.2/15.3.3.2-1.js
 * @description Function.length - data property with value 1
 */


function testcase() {

  var desc = Object.getOwnPropertyDescriptor(Function,"length");
  if(desc.value === 1 &&
     desc.writable === false &&
     desc.enumerable === false &&
     desc.configurable === false)
    return true; 

 }
runTestCase(testcase);
