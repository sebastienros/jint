/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-6.js
 * @description Array.prototype.every returns true if 'length' is 0 (subclassed Array, length overridden with obj with valueOf)
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  
  var o = { valueOf: function () { return 0;}};
  f.length = o;
  
  function cb(){}
  var i = f.every(cb);
  
  if (i === true) {
    return true;
  }
 }
runTestCase(testcase);
