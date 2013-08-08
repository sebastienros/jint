/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-6.js
 * @description Array.prototype.filter returns an empty array if 'length' is 0 (subclassed Array, length overridden with obj with valueOf)
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  
  var o = { valueOf: function () { return 0;}};
  f.length = o;
  
  function cb(){}
  var a = f.filter(cb);
  
  if (Array.isArray(a) &&
      a.length === 0) {
    return true;
  }
 }
runTestCase(testcase);
