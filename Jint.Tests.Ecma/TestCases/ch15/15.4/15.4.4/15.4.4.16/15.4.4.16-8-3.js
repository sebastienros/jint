/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-3.js
 * @description Array.prototype.every returns true if 'length' is 0 (subclassed Array, length overridden to false (type conversion))
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = false;

  function cb(){}
  var i = f.every(cb);
  
  if (i === true) {
    return true;
  }
 }
runTestCase(testcase);
