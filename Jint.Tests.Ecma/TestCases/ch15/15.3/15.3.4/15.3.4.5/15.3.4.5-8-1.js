/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-8-1.js
 * @description Function.prototype.bind, type of bound function must be 'function'
 */


function testcase() {
  function foo() { }
  var o = {};
  
  var bf = foo.bind(o);
  if (typeof(bf) === 'function') {
    return  true;
  }
 }
runTestCase(testcase);
