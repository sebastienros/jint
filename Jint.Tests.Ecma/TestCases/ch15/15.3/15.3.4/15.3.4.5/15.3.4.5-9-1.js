/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-9-1.js
 * @description Function.prototype.bind, [[Prototype]] is Function.prototype
 */


function testcase() {
  function foo() { }
  var o = {};
  
  var bf = foo.bind(o);
  if (Function.prototype.isPrototypeOf(bf)) {
    return true;
  }
 }
runTestCase(testcase);
