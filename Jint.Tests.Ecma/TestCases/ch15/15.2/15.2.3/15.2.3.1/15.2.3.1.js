/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.1/15.2.3.1.js
 * @description Object.prototype is a data property with default attribute values (false)
 */


function testcase() {
  var desc = Object.getOwnPropertyDescriptor(Object, 'prototype');
  if (desc.writable === false &&
      desc.enumerable === false &&
      desc.configurable === false) {
    return true;
  }
 }
runTestCase(testcase);
