/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.7/15.7.4/15.7.4-1.js
 * @description Number prototype object: its [[Class]] must be 'Number'
 */


function testcase() {
  var numProto = Object.getPrototypeOf(new Number(42));
  var s = Object.prototype.toString.call(numProto );
  return (s === '[object Number]') ;
 }
runTestCase(testcase);
