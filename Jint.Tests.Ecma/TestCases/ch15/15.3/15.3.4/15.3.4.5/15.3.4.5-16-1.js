/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-16-1.js
 * @description Function.prototype.bind, [[Extensible]] of the bound fn is true
 */


function testcase() {
  function foo() { }
  var o = {};
  
  var bf = foo.bind(o);
  var ex = Object.isExtensible(bf);
  if (ex === true) {
    return true;
  }
 }
runTestCase(testcase);
