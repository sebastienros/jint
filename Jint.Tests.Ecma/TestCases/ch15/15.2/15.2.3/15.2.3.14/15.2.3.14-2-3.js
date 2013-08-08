/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-3.js
 * @description Object.keys returns the standard built-in Array (Array overridden)
 */


function testcase() {
  function Array() { }

  var o = { x: 1, y: 2};

  var a = Object.keys(o);

  var s = Object.prototype.toString.call(a);
  if (s === '[object Array]') {
    return true;
  }
 }
runTestCase(testcase);
