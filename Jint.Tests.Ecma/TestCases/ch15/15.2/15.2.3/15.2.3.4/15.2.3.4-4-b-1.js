/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-1.js
 * @description Object.getOwnPropertyNames - descriptor of resultant array is all true
 */


function testcase() {
  var obj = new Object();
  obj.x = 1;
  obj.y = 2;
  var result = Object.getOwnPropertyNames(obj);
  var desc = Object.getOwnPropertyDescriptor(result,"0");
  if (desc.enumerable === true &&
      desc.configurable === true &&
      desc.writable === true) {
    return true;
  }
 }
runTestCase(testcase);
