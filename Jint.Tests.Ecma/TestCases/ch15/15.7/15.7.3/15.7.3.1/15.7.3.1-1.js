/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.7/15.7.3/15.7.3.1/15.7.3.1-1.js
 * @description Number.prototype is a data property with default attribute values (false)
 */


function testcase() {
  var d = Object.getOwnPropertyDescriptor(Number, 'prototype');
  
  if (d.writable === false &&
      d.enumerable === false &&
      d.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
